# Build context is the repo root, so this can sit alongside the other services' Dockerfiles
# uniformly in docker-compose.yml. Vite inlines VITE_* env vars at build time, not runtime — the
# image must be rebuilt if the public Api URL or Agora App ID changes.
FROM node:22-alpine AS build
WORKDIR /app

COPY frontend/nightfall-app/package.json frontend/nightfall-app/package-lock.json ./
RUN npm ci

COPY frontend/nightfall-app/ ./
ARG VITE_API_BASE_URL
ARG VITE_AGORA_APP_ID
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
ENV VITE_AGORA_APP_ID=$VITE_AGORA_APP_ID
RUN npm run build

FROM nginx:1.27-alpine AS runtime
COPY --from=build /app/dist /usr/share/nginx/html
COPY frontend/nightfall-app/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
