#!/bin/fish

set -l REPOSITORY_URL "andvaris-forge:5000"
set -l IMAGE_NAME "fr-wireplumber-docs"
set -l TAG "latest"

docfx docfx.json
 
if test $status -ne 0
    echo "Couldn't build documentation"
    exit 1
end

podman build -f Dockerfile-Docs -t $REPOSITORY_URL/$IMAGE_NAME:$TAG .

if test $status -ne 0
    echo "Couldn't build container"
    exit 1
end

podman push "$REPOSITORY_URL/$IMAGE_NAME:$TAG"
 
if test $status -ne 0
    echo "Couldn't push image"
    exit 1
end

ssh andvaris-forge 'systemctl --user restart fr-wireplumber-docs'
