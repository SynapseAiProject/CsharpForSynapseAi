#!/bin/bash

solution=$($HUB_SCRIPT_DIR/scripts/sln.sh)
org="${solution%.*}"
host="Host/Api/$org.Host.csproj"
echo $host
