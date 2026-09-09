#!/bin/bash

if [ "$#" -ne 4 ]; then
	echo As 4 Camadas são necessárias!
	echo Tente dessa forma: "$0 <domain_path> <application_path> <infra_path> <tests_path>"
	exit 1
fi

DOMAIN="$1"
APPLICATION="$2"
INFRA="$3"
TESTS="$4"

solution=$($HUB_SCRIPT_DIR/scripts/sln.sh)

dotnet sln $solution add $DOMAIN $APPLICATION $INFRA $TESTS

dotnet add $APPLICATION reference $DOMAIN
dotnet add $INFRA reference $APPLICATION
dotnet add $TESTS reference $INFRA

host=$($HUB_SCRIPT_DIR/scripts/host.sh)

dotnet add $host reference $INFRA

kernel=$($HUB_SCRIPT_DIR/scripts/createKernelTecnico.sh -n)

if [[ "$DOMAIN" != *"$kernel"* ]]; then
	layers=($($HUB_SCRIPT_DIR/scripts/createKernelTecnico.sh -layer))
	dotnet add $DOMAIN reference "${layers[0]}"
	dotnet add $APPLICATION reference "${layers[1]}"
	dotnet add $INFRA reference "${layers[2]}"
fi
