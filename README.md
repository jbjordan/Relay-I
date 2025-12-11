# Relay-I

Play the Demo: https://mrtathon2025-temp.itch.io/relay-i-demo

## Background
There are over 200,000 video games available on itch.io, a popular website for indie game developers to publish their demos and products. Unfortunately, the analytics provided by itch.io are somewhat limited. Because of this, developers may be unaware of important player session metrics, such as time played, progress, exceptions encountered, and other events.

The purpose of this project is to provide logging functionality for Godot games utilizing a Relay server that can be hosted anywhere. AI will then be used to analyze these logs and provide actionable insight. This can help developers understand how players interact with their game, and where to make improvements.

There are three main parts to this project:
1. Create REST server using Relay to enable local behavioral logging
2. Add logging to an existing video game hosted on itch.io
3. Utilize AI to analyze logs and create actionable insights

## Setup

This scenario requires the following:

1. A Relay Hybrid Connection resource
2. Microsoft Foundry AI Agent resource
3. .NET 10 to run RelayLogServer and Foundry Agent

## Relay Server

The Relay Server uses Microsoft.Azure.Relay to create a listener to send and receive messages using the HTTP protocol.
You can read more about Azure Relay here: https://learn.microsoft.com/en-us/azure/azure-relay/relay-what-is-it

To run the server:
1. Create a SAS key for your Hybrid Connection resource through the portal
2. Store it in env variable named: relay_conn_string
3. Run server with Visual Studio

Logs are stored locally in relayserver.log file.

## Logging in Godot

HTTPRequest node is used in the relay_client.gd script (in logging folder) to send logs to the Relay server.
This game is based on Brackey's tutorial: https://github.com/Brackeys/first-game-in-godot

To enable logging:
1. Add relay_client.gd to your game and make it an Autoload
2. Update relay_endpoint with your Hybrid Connection Endpoint resource
3. Create a dictionary of strings with whatever data you want and log it from anywhere. For example:

```gdscript
var relay_log_dict = { "Event" : "PlayerTimer", "Location" : str(global_position), "LevelId" : "1" }
RelayClient.log_game_event(relay_log_dict)
```
PII is not collected.

## AI Analysis

The FoundryServer creates an agent with pre-made instructions, and provides the logs collected by the Relay Server.
The results are then written to a local file for review.

It is based on this tutorial: https://ai.azure.com/doc/azure/ai-foundry/agents/how-to/tools/file-search-upload-files?tid=72f988bf-86f1-41af-91ab-2d7cd011db47

To run agent:
1. Create Microsoft Foundry Project
2. Add project url to env variable named: FOUNDY_AGENT_URL
3. Ensure log file is in appropriate location
4. Run with Visual Studio
