#!/bin/bash

# Define paths
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SERVICE_DIR="$ROOT_DIR/Agent.Service"
MAC_DIR="$ROOT_DIR/Agent.Mac"

# Kill existing processes
echo "Stopping existing agents..."
pkill -f "Agent.Service"
pkill -f "Agent.Mac"

# Clear DB if requested
if [ "$1" == "--clear-db" ]; then
    echo "Clearing Agent Database..."
    rm -rf "$HOME/Library/Application Support/EmployeeTracker/agent.db"
    rm -rf "$HOME/Library/Application Support/EmployeeTracker/agent.db-shm"
    rm -rf "$HOME/Library/Application Support/EmployeeTracker/agent.db-wal"
fi

# Build
echo "Building Agent.Service..."
dotnet build "$SERVICE_DIR"
if [ $? -ne 0 ]; then
    echo "Agent.Service build failed."
    exit 1
fi

echo "Building Agent.Mac..."
dotnet build "$MAC_DIR"
if [ $? -ne 0 ]; then
    echo "Agent.Mac build failed."
    exit 1
fi

# Run
echo "Starting Agent.Service..."
dotnet run --project "$SERVICE_DIR" &
SERVICE_PID=$!

echo "Waiting for Service to start..."
sleep 5

echo "Starting Agent.Mac..."
export AGENT_LOCAL_API_TOKEN="dev-token-123"
dotnet run --project "$MAC_DIR" &
MAC_PID=$!

echo "Agents running. Service PID: $SERVICE_PID, Mac PID: $MAC_PID"
echo "Press Ctrl+C to stop."

wait
