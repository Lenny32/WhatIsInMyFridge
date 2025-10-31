#!/bin/sh

# This script runs at container startup to inject runtime environment variables into the built app
# Since Vite builds static files, we need to replace the API base URL at runtime

# Replace the API base URL in the built JavaScript files
if [ -n "$VITE_API_BASE" ]; then
  echo "Configuring API base URL to: $VITE_API_BASE"
  
  # Find all JS files and replace the placeholder
  find /usr/share/nginx/html/assets -type f -name "*.js" -exec sed -i "s|__VITE_API_BASE__|$VITE_API_BASE|g" {} \;
else
  echo "Warning: VITE_API_BASE not set, using default"
fi

# Start nginx
exec nginx -g "daemon off;"
