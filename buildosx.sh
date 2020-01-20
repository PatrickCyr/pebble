mkdir bin/pebble
mcs -d:PEBBLECLI -out:bin/pebble src/*.cs src/coco/*.cs src/lib/*.cs
