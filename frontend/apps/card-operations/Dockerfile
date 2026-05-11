FROM node:22-alpine AS build
WORKDIR /app

COPY frontend/apps/card-operations/package.json frontend/apps/card-operations/package-lock.json* ./
RUN npm ci

COPY frontend/apps/card-operations/ .
RUN npx vite build

FROM nginxinc/nginx-unprivileged:alpine
COPY --from=build /app/dist /usr/share/nginx/html
COPY frontend/apps/card-operations/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 8080
USER nginx
CMD ["nginx", "-g", "daemon off;"]
