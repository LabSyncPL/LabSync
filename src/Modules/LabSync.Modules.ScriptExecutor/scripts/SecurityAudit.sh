#!/bin/bash
echo "--- LabSync Security Quick-Check ---"

echo "[1/3] Checking currently logged-in users..."
who
sleep 1

echo "[2/3] Checking last 5 logins..."
last -n 5
sleep 1

echo "[3/3] Checking for root-level processes..."
ps -U root -u root u | head -n 5
sleep 1

echo "100% - Security snapshot complete."