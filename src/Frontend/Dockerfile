FROM node:22-alpine AS build
WORKDIR /app
COPY src/Frontend/package.json src/Frontend/package-lock.json ./
RUN npm ci
COPY src/Frontend/ ./
# Use the same origin as Nginx. API paths already start with /api in Axios.
ARG VITE_API_BASE_URL=/
ENV VITE_API_BASE_URL=${VITE_API_BASE_URL} \
    VITE_USE_MOCK_DATA=false
RUN npm run build

FROM nginx:stable-alpine AS runtime
ENV API_UPSTREAM=http://backend-api:8080 \
    NGINX_ENVSUBST_FILTER=^API_UPSTREAM$
COPY deploy/web/default.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist/ /usr/share/nginx/html/
EXPOSE 80
HEALTHCHECK --interval=15s --timeout=3s --start-period=5s --retries=3 \
    CMD wget -q -O /dev/null http://127.0.0.1/healthz || exit 1
