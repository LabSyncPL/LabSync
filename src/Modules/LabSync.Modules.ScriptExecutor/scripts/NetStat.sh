#!/bin/bash
echo "--- LabSync: Linux Network Interface Report ---"

echo "Current IP Addresses:"
ip -br addr show | awk '{print "[Interface] " $1 " -> " $3}'
sleep 1
echo "Progress: 33%"

echo "Default Gateway:"
ip route | grep default
sleep 1
echo "Progress: 66%"

echo "DNS Configuration (/etc/resolv.conf):"
grep "nameserver" /etc/resolv.conf
sleep 1

echo "100% - Network report generated."