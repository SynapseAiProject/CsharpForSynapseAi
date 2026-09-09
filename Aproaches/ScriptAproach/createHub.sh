#!/bin/bash

if [ "$#" -ne 1 ]; then
	echo O nome do Hub precisa ser informado!;
	echo Tente dessa forma: "$0 <hubName>";
	exit 1;
fi

name="$1"

mkdir -p $name

dotnet new sln -n $name -o $name
cd $name

mkdir -p Host
dotnet new console -n "$name.Host" -o Host/Api

if ! $HUB_SCRIPT_DIR/scripts/createKernelTecnico.sh; then
	exit 1
fi
