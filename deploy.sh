#!/bin/bash
set -e

MODS_DIR="$HOME/Library/Application Support/Steam/steamapps/common/A Dance of Fire and Ice/Mods/Bismuth"

xbuild Bismuth.sln > /dev/null

mkdir -p "$MODS_DIR/Resources"
cp Bismuth/bin/Debug/Bismuth.dll "$MODS_DIR/"
cp Info.json "$MODS_DIR/"
cp Bismuth/Resources/bismuth-fonts "$MODS_DIR/Resources/"

echo "Deployed to $MODS_DIR"
