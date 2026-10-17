# Stage 1: Build frontend
FROM node:22-alpine AS builder

WORKDIR /app

# Copy package manifests
COPY package.json package-lock.json* ./

# Install dependencies
RUN npm install

# Copy application source
COPY . .

# Support build-time API URL configuration with production fallback
ARG VITE_API_URL=https://stardew-api.ijplabs.dev
ENV VITE_API_URL=$VITE_API_URL

# Build production assets
RUN npm run build

# Stage 2: Production Nginx Server
FROM nginx:alpine

# Clear default static files
RUN rm -rf /usr/share/nginx/html/*

# Copy build artifacts
COPY --from=builder /app/dist /usr/share/nginx/html

# Copy custom nginx configuration
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80 3000

CMD ["nginx", "-g", "daemon off;"]
