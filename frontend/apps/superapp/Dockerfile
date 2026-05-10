FROM node:22-alpine AS build
WORKDIR /app

COPY frontend/apps/superapp/package.json frontend/apps/superapp/package-lock.json* ./
RUN npm ci

COPY frontend/apps/superapp/ .
RUN npx vite build

FROM nginxinc/nginx-unprivileged:alpine
COPY --from=build /app/dist /usr/share/nginx/html
COPY frontend/apps/superapp/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 8080
CMD ["nginx", "-g", "daemon off;"]
