#!/bin/bash
echo "--- Scanning System Logs for Errors (Last 10m) ---"

ERRORS=$(journalctl --since "10 minutes ago" --priority=err --no-pager | head -n 20)

if [ -z "$ERRORS" ]; then
    echo "No critical errors found in the last 10 minutes. System healthy."
else
    echo "CRITICAL ERRORS FOUND:"
    echo "$ERRORS"
fi

echo "Progress: 100%"
echo "Execution finished."