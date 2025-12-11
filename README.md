# Relay-I

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
2. .NET 10 to run RelayLogServer and Foundry Agent
4. Microsoft Foundry AI Agent resource

## Relay Server

The Relay Server uses Microsoft.Azure.Relay to create a listener

## Logging in Godot

In progress

## AI Analysis

In progress