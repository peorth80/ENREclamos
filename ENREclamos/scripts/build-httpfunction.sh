#!/bin/bash
cd ../src/ENREclamos.LambdaHTTPFunction
dotnet lambda package --output-package output.zip --configuration Release --framework net6.0
