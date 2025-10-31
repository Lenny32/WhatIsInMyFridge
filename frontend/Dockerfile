# Build stage
FROM denoland/deno:2.0.6 AS build
WORKDIR /app

# Copy package files
COPY deno.json deno.lock ./
COPY tsconfig.json tsconfig.node.json ./
COPY vite.config.ts svelte.config.js ./

# Cache dependencies
RUN deno install

# Copy source files
COPY index.html ./
COPY src/ ./src/

# Build the application
RUN deno task build

# Runtime stage - serve with nginx
FROM nginx:alpine
WORKDIR /usr/share/nginx/html

# Remove default nginx static assets
RUN rm -rf ./*

# Copy built assets from build stage
COPY --from=build /app/dist .

# Copy nginx configuration
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Copy entrypoint script for runtime env var injection
COPY docker-entrypoint.sh /docker-entrypoint.sh
RUN chmod +x /docker-entrypoint.sh

# Expose port
EXPOSE 80

ENTRYPOINT ["/docker-entrypoint.sh"]
