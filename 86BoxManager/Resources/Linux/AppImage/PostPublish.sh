#!/bin/bash
# This is a dummy bash script used for demonstration and test. It outputs a few variables
# and creates a dummy file in the application directory which will be detected by the program.

echo
echo "==========================="
echo "POST_PUBLISH BASH SCRIPT"
echo "==========================="
echo

# Some useful macros / environment variables
echo "BUILD_ARCH: ${BUILD_ARCH}"
echo "BUILD_TARGET: ${BUILD_TARGET}"
echo "BUILD_SHARE: ${BUILD_SHARE}"
echo "BUILD_APP_BIN: ${BUILD_APP_BIN}"
echo

cp -r ./bin/* ${BUILD_APP_BIN}

echo
echo "==========================="
echo "POST_PUBLISH END"
echo "==========================="
echo
