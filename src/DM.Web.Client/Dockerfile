# Build stage
FROM node:20-alpine AS build

WORKDIR /app

# Copy package files
COPY package.json package-lock.json ./

# Install dependencies
RUN npm ci

# Copy source
COPY . .

# The API host is empty by default so the SPA calls relative URLs and nginx
# routes them; a deployment that serves the API from another origin overrides it.
ARG VITE_API_HOST=""
ENV VITE_API_HOST=$VITE_API_HOST
# build-only, not build: "build" runs the type check first, and the type check
# reads tsconfig.e2e.json, whose include is e2e/** - a directory .dockerignore
# keeps out of the context. Inside the image that config matches nothing and
# tsc stops with TS18003, so every build of this image failed. The type check
# belongs to the frontend CI job, which publish-frontend and the deployment
# smoke both wait on, so no image can carry code that never passed it.
RUN npm run build-only

# Production stage
FROM nginx:1.27-alpine

# Copy built files
COPY --from=build /app/dist /usr/share/nginx/html

# Copy nginx config
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
