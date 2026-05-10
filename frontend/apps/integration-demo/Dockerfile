FROM node:22-alpine AS build
WORKDIR /app

COPY frontend/apps/integration-demo/package.json frontend/apps/integration-demo/package-lock.json* ./
RUN npm ci

COPY frontend/apps/integration-demo/ .
RUN npx vite build

FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html
COPY frontend/apps/integration-demo/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 8080
USER nginx
CMD ["nginx", "-g", "daemon off;"]
