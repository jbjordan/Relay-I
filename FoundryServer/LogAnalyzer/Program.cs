using Azure; // This requires Azure.Core NuGet package
using Azure.AI.Agents.Persistent; // This requires Azure.AI.Agents.Persistent NuGet package
using Azure.Core;
using Azure.Identity; // This requires Azure.Identity NuGet package
using Microsoft.Extensions.Configuration; // Add this for ConfigurationBuilder extensions
using System;
using System.Collections.Generic;
using System.IO; // Add this for File.Exists
using System.Threading;

Console.WriteLine("Starting Game Log AI Analysis");

var agentUrl = Environment.GetEnvironmentVariable("FOUNDY_AGENT_URL");

var projectEndpoint = agentUrl;
var modelDeploymentName = "gpt-5-nano";

string gameInstructions = "I will provide a text file that contains logs for a video game, and I would like you analyze and provide actionable insights. \r\n\r\nHere is some information about the game logs:\r\n1. TrackingID corresponds to a single session by a player.\r\n2. ElapsedTime is the running time played for the session.\r\n3. LevelId corresponds to the current level.\r\n\r\nHere is some information specific to the game being played:\r\n1. The game has two levels.\r\n\r\nHere are some specific questions I would like you to answer:\r\n1. How many unique sessions?\r\n2. What is the average session length?\r\n3. What is the longest session length?\r\n4. What is the shortest session length?\r\n5. What is the most common event?\r\n6. What is the least common event?\r\n7. What levels have the most time?\r\n8. How often do players die?\r\n9. Where do players die most often?\r\n10. What do you suggest to increase the average session length?\r\n11. Are there any likely bugs based on the data?";

// Create the Agent Client
PersistentAgentsClient agentClient = new(projectEndpoint, new DefaultAzureCredential());

// Upload local sample file to the agent
PersistentAgentFileInfo uploadedAgentFile = agentClient.Files.UploadFile(
    filePath: "relayserver_v1.txt",
    purpose: PersistentAgentFilePurpose.Agents
);

// Setup dictionary with list of File IDs for the vector store
Dictionary<string, string> fileIds = new()
{
    { uploadedAgentFile.Id, uploadedAgentFile.Filename }
};

// Create a vector store with the file and wait for it to be processed.
// If you do not specify a vector store, CreateMessage will create a vector
// store with a default expiration policy of seven days after they were last active
PersistentAgentsVectorStore vectorStore = agentClient.VectorStores.CreateVectorStore(
    fileIds: new List<string> { uploadedAgentFile.Id },
    name: "my_vector_store");

// Create tool definition for File Search
FileSearchToolResource fileSearchToolResource = new FileSearchToolResource();
fileSearchToolResource.VectorStoreIds.Add(vectorStore.Id);

Console.WriteLine("Creating AI Agent");
// Create an agent with Tools and Tool Resources
PersistentAgent agent = agentClient.Administration.CreateAgent(
        model: modelDeploymentName,
        name: "Game Log Analysis",
        instructions: gameInstructions,
        tools: new List<ToolDefinition> { new FileSearchToolDefinition() },
        toolResources: new ToolResources() { FileSearch = fileSearchToolResource });

Console.WriteLine("Agent is analyzing the logs...");
// Create the agent thread for communication
PersistentAgentThread thread = agentClient.Threads.CreateThread();

// Create message and run the agent
PersistentThreadMessage messageResponse = agentClient.Messages.CreateMessage(
    thread.Id,
    MessageRole.User,
    "Analyze the logs in the file provided");

ThreadRun run = agentClient.Runs.CreateRun(thread, agent);

// Wait for the agent to finish running
do
{
    Thread.Sleep(TimeSpan.FromMilliseconds(500));
    run = agentClient.Runs.GetRun(thread.Id, run.Id);
}
while (run.Status == RunStatus.Queued
    || run.Status == RunStatus.InProgress);

// Confirm that the run completed successfully
if (run.Status != RunStatus.Completed)
{
    throw new Exception("Run did not complete successfully, error: " + run.LastError?.Message);
}

// Retrieve all messages from the agent client
Pageable<PersistentThreadMessage> messages = agentClient.Messages.GetMessages(
    threadId: thread.Id,
    order: ListSortOrder.Ascending
);

// Helper method for replacing references
static string replaceReferences(Dictionary<string, string> fileIds, string fileID, string placeholder, string text)
{
    if (fileIds.TryGetValue(fileID, out string replacement))
        return text.Replace(placeholder, $" [{replacement}]");
    else
        return text.Replace(placeholder, $" [{fileID}]");
}

Console.WriteLine("Analysis complete, writing results to file.");
// Process messages in order
foreach (PersistentThreadMessage threadMessage in messages)
{
    //Console.Write($"{threadMessage.CreatedAt:yyyy-MM-dd HH:mm:ss} - {threadMessage.Role,10}: ");

    foreach (MessageContent contentItem in threadMessage.ContentItems)
    {
        if (contentItem is MessageTextContent textItem)
        {
            if (threadMessage.Role == MessageRole.Agent && textItem.Annotations.Count > 0)
            {
                string strMessage = textItem.Text;

                // If we file path or file citation annotations - rewrite the 'source' FileId with the file name
                foreach (MessageTextAnnotation annotation in textItem.Annotations)
                {
                    if (annotation is MessageTextFilePathAnnotation pathAnnotation)
                    {
                        strMessage = replaceReferences(fileIds, pathAnnotation.FileId, pathAnnotation.Text, strMessage);
                    }
                    else if (annotation is MessageTextFileCitationAnnotation citationAnnotation)
                    {
                        strMessage = replaceReferences(fileIds, citationAnnotation.FileId, citationAnnotation.Text, strMessage);
                    }
                }
                File.WriteAllText("analysis.txt", strMessage);
            }
            else
            {
                Console.Write("");
                //Console.Write(textItem.Text);
            }
        }
        else if (contentItem is MessageImageFileContent imageFileItem)
        {
            Console.Write($"<image from ID: {imageFileItem.FileId}");
        }
    }
}

Console.WriteLine("Results written to analysis.txt");

return 1;