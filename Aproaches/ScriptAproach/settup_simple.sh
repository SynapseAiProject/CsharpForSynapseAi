#!/bin/bash

solutions=( *.slnx )

if [ "${#solutions[@]}" -ne 1 ]; then
	echo Precisa ser chamado na raiz da solution!;
	echo Precisa ter apenas uma solution!;
fi

solution="${solutions[0]}"

# --------------- <Begin HOST /> ---------------
HOST_API="Host/Api/${solution%.slnx}.Api.csproj"
HOST_TESTS="Host/Api/${solution%.slnx}.Api.csproj"

if [ ! -d "Host" ]; then
	mkdir Host

	dotnet new console -n "${solution%.slnx}.Api" -o Host/Api
	dotnet new xunit -n "${solution%.slnx}.Tests" -o Host/Tests

	dotnet sln $solution add $HOST_API $HOST_TESTS
	dotnet add $HOST_TESTS reference $HOST_API
fi

# ---------------- <End HOST /> ----------------

# --------------- <Begin BOUNDED_CONTEXTS /> ---------------
bcDir="BoundedContexts"
kernelTecnico="Kernel.Tecnico"

if [ ! -d $bcDir ]; then
	mkdir $bcDir
	mkdir $bcDir/$kernelTecnico

	echo new classlib "$bcDir.$kernelTecnico.Domain" -o $bcDir/$kernelTecnico/Domain
	dotnet new classlib -n "$bcDir.$kernelTecnico.Domain" -o $bcDir/$kernelTecnico/Domain

	echo new classlib "$bcDir.$kernelTecnico.Application" -o $bcDir/$kernelTecnico/Application
	dotnet new classlib -n "$bcDir.$kernelTecnico.Application" -o $bcDir/$kernelTecnico/Application

	echo new classlib "$bcDir.$kernelTecnico.Infrastructure" -o $bcDir/$kernelTecnico/Infrastructure
	dotnet new classlib -n "$bcDir.$kernelTecnico.Infrastructure" -o $bcDir/$kernelTecnico/Infrastructure

	echo new classlib "$bcDir.$kernelTecnico.Tests" -o $bcDir/$kernelTecnico/Tests
	dotnet new xunit -n "$bcDir.$kernelTecnico.Tests" -o $bcDir/$kernelTecnico/Tests
fi

SHARED_DOMAIN="$bcDir/$kernelTecnico/Domain/$kernelTecnico.Domain.csproj"
SHARED_APPLICATION="$bcDir/$kernelTecnico/Application/$kernelTecnico.Application.csproj"
SHARED_INFRA="$bcDir/$kernelTecnico/Infrastructure/$kernelTecnico.Infrastructure.csproj"

# --------------- <End BOUNDED_CONTEXTS /> ---------------

# --------------- <Begin SOLUTION _LOOP /> ---------------

for bc in $bcDir/*; do
    name=$(basename "$bc")

    domain="$bc/Domain/$bcDir.$name.Domain.csproj"
    application="$bc/Application/$bcDir.$name.Application.csproj"
    infra="$bc/Infrastructure/$bcDir.$name.Infrastructure.csproj"
	tests="$bc/Tests/$bcDir.$name.Tests.csproj"

	echo add to solution:
	echo -e "\t$domain"
	echo -e "\t$application"
	echo -e "\t$infra"
	echo -e "\t$tests"
	dotnet sln $solution add $domain $application $infra $tests

    dotnet add "$application" reference "$domain"
    dotnet add "$infra" reference "$application"
	dotnet add "$tests" reference "$infra"

	case "$name" in
        "$kernelTecnico") continue ;;
    esac

    dotnet add "$domain" reference "$SHARED_DOMAIN"
    dotnet add "$application" reference "$SHARED_APPLICATION"
    dotnet add "$infra" reference "$SHARED_INFRA"
	dotdet add "$HOST_API" reference "$infra"
done

# --------------- <End SOLUTION _LOOP /> ---------------
