#!/bin/bash

shopt -s nullglob

if [ "$#" -gt 0 ]; then
	path=$1
	if ! cd $path; then
		exit 1
	fi
fi

solutions=( *.slnx )

if [ "${#solutions[@]}" -ne 1 ]; then
	echo Precisa ser chamado na raiz da solution!;
	echo Precisa ter apenas uma solution!;
	exit 1
fi

solution="${solutions[0]}"
echo $solution
