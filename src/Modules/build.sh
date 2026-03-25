#!/bin/bash

# Iterate through directories starting with LabSync.Modules.
for dir in LabSync.Modules.*/ ; do
    dir=${dir%/}
    echo "--- Building: $dir ---"
    
    dotnet build "$dir"
    if [ $? -ne 0 ]; then
        echo "[ERROR] Build failed for $dir"
    fi
    echo ""
done