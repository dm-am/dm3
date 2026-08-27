# Build stage
#
# The exact patch, not the major. npm ships inside the Node tarball, and the
# lock file it writes is read back by whichever npm the next machine happens to
# have: 24-alpine floated onto a patch carrying npm 11.17.0 while the file had
# been written by the 11.6.2 that comes with 24.13.0, and `npm ci` refused it.
# Whoever raises this raises package.json and the workflow with it - the version
# is one decision in three files, and FrontendToolchainShould holds them equal.
FROM node:24.13.0-alpine AS build

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
# belongs to the frontend CI job, which publish-frontend waits on, so no
# published image can carry code that never passed it. The deployment smoke
# builds this image without that gate: it declares no needs at all.
RUN npm run build-only

# Production stage
FROM nginx:1.27-alpine

# Copy built files
COPY --from=build /app/dist /usr/share/nginx/html

# Copy nginx config
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
