#!/bin/bash
echo "--- Starting 10-second CPU Stress Simulation ---"

for i in {1..10}
do
   echo "Stress Level $i/10: Calculating primes..."
   # Simple math loop to keep the CPU busy for a second
   (echo "scale=2000; a(1)*4" | bc -l) > /dev/null & 
   sleep 1
   prog=$((i * 10))
   echo "Progress: $prog%"
done

echo "Current System Temperature:"
cat /sys/class/thermal/thermal_zone0/temp 2>/dev/null || echo "Thermal data N/A"

echo "100% - Stress test finished."