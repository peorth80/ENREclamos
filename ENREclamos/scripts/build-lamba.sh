#!/bin/bash
cd ../src/ENREclamos
dotnet lambda package --output-package output.zip --configuration Release --framework net6.0
