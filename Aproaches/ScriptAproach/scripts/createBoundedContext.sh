#!/bin/bash

bcDir="BoundedContexts"

if [ "$#" -lt 1 ]; then
	echo O nome do Bounded Context precisa ser informado!;
	echo Tente dessa forma: "$0 <boundedContextName>";
	exit 1;
fi

name="$1"

if [ "$name" = "-n" ]; then
	echo $bcDir
	exit 0
fi

if [ "$#" -gt 1 ]; then

	type="$2"
	if [ "$type" = "-layer" ]; then
		DOMAIN="$bcDir/$name/Domain/$bcDir.$name.Domain.csproj"
		APPLICATION="$bcDir/$name/Application/$bcDir.$name.Application.csproj"
		INFRA="$bcDir/$name/Infrastructure/$bcDir.$name.Infrastructure.csproj"
		TESTS="$bcDir/$name/Tests/$bcDir.$name.Tests.csproj"
		echo "$DOMAIN $APPLICATION $INFRA $TESTS"
		exit 0
	fi
fi

solutions=( *.slnx )

if  ! $HUB_SCRIPT_DIR/scripts/sln.sh ; then
	exit 1;
fi

bcDir="BoundedContexts"


mkdir -p $bcDir

if [ ! -d $bcDir/$name ]; then
	mkdir -p $bcDir/$name
	echo new classlib "$bcDir.$name.Domain" -o $bcDir/$name/Domain
	dotnet new classlib -n "$bcDir.$name.Domain" -o $bcDir/$name/Domain

	echo new classlib "$bcDir.$name.Application" -o $bcDir/$name/Application
	dotnet new classlib -n "$bcDir.$name.Application" -o $bcDir/$name/Application

	echo new classlib "$bcDir.$name.Infrastructure" -o $bcDir/$name/Infrastructure
	dotnet new classlib -n "$bcDir.$name.Infrastructure" -o $bcDir/$name/Infrastructure

	echo new classlib "$bcDir.$name.Tests" -o $bcDir/$name/Tests
	dotnet new xunit -n "$bcDir.$name.Tests" -o $bcDir/$name/Tests
fi
