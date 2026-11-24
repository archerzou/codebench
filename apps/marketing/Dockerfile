# Build context: apps/marketing. docs/design and problems arrive as the additional build contexts "design" and
# "problems" (compose additional_contexts / images.yml build-contexts), mounted at the same relative paths as on
# your machine so `../../../../docs/design/tokens.css` and `../../problems` resolve identically (guide 04).
# problems/ is used by scripts/gen-problems.mjs in the BUILD stage only: the final image gets dist/ — summaries,
# re-checked by scripts/verify-dist.mjs during `npm run build` (FE-13, RA-06).
FROM node:24-alpine AS build
WORKDIR /repo/apps/marketing
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
COPY --from=design . /repo/docs/design
COPY --from=problems . /repo/problems
ARG SITE_URL=http://codebench.localhost
ARG APP_URL=http://app.codebench.localhost
ENV SITE_URL=$SITE_URL PUBLIC_APP_URL=$APP_URL
RUN npm run build

FROM nginx:1.29-alpine
COPY --from=build /repo/apps/marketing/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
