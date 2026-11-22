# Build context: apps/web. docs/design arrives as the additional build context "design" (compose
# additional_contexts / images.yml build-contexts), mounted at the same relative path as on your machine so
# src/index.css's `@import "../../../docs/design/tokens.css"` resolves identically (guide 04).
FROM node:24-alpine AS build
WORKDIR /repo/apps/web
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
COPY --from=design . /repo/docs/design
RUN npm run build

FROM nginx:1.27-alpine
COPY --from=build /repo/apps/web/dist /usr/share/nginx/html
# The dev stub is replaced at start by 40-config.sh from the container's environment.
RUN rm -f /usr/share/nginx/html/config.js
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY docker/40-config.sh /docker-entrypoint.d/40-config.sh
# Strip CRs in case the script was checked out on Windows with autocrlf; then make it executable.
RUN sed -i 's/\r$//' /docker-entrypoint.d/40-config.sh && chmod +x /docker-entrypoint.d/40-config.sh
