name="Kernel.Tecnico"

bcDir=$($HUB_SCRIPT_DIR/scripts/createBoundedContext.sh -n)

KERNEL_DOMAIN="$bcDir/$name/Domain/$bcDir.$name.Domain.csproj"
KERNEL_APPLICATION="$bcDir/$name/Application/$bcDir.$name.Application.csproj"
KERNEL_INFRA="$bcDir/$name/Infrastructure/$bcDir.$name.Infrastructure.csproj"
KERNEL_TESTS="$bcDir/$name/Tests/$bcDir.$name.Tests.csproj"

if [ "$#" -gt 0 ]; then

	type="$1"

	if [ "$type" = "-n" ]; then
		echo $name
		exit 0
	fi

	if [ "$type" = "-layer" ]; then
		$HUB_SCRIPT_DIR/scripts/createBoundedContext.sh $name -layer
		exit 0
	fi

	if [[ "$type" != *"-"* ]]; then
		echo "Wrong charactere '-'"
		exit 1
	fi

fi

if ! $HUB_SCRIPT_DIR/scripts/createBoundedContext.sh "$name" ; then
	exit 1
fi

if ! $HUB_SCRIPT_DIR/scripts/addBCRelations.sh $($HUB_SCRIPT_DIR/scripts/createBoundedContext.sh $name -layer); then
	exit 1
fi
