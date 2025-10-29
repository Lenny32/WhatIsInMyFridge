#!/bin/bash
cd "$(dirname "$0")"
exec deno task dev
