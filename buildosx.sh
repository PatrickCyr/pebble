mkdir bin/pebble
mcs -d:PEBBLE_CLI -out:bin/pebble src/*.cs src/coco/*.cs src/lib/*.cs
