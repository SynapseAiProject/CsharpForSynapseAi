#!/bin/bash
solution=$($HUB_SCRIPT_DIR/scripts/sln.sh $2)
hasSolution="$?"

if [ "$hasSolution" -ne 0 ]; then
	echo Error: O diretorio atual nao e uma solution. E nem ha referencia a uma!
	exit 1
fi
org="${solution%.slnx}"
echo org $org
if ! cd $org; then
	exit 1
fi


if ! $HUB_SCRIPT_DIR/scripts/createBoundedContext.sh "$@" ; then
	exit 1
fi
echo addBCRelations $($HUB_SCRIPT_DIR/scripts/createBoundedContext.sh "$1" -layer)
if ! $HUB_SCRIPT_DIR/scripts/addBCRelations.sh $($HUB_SCRIPT_DIR/scripts/createBoundedContext.sh "$1" -layer); then
	exit 1
fi
