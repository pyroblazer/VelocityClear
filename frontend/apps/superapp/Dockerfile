FROM node:22-alpine AS build
WORKDIR /app

COPY frontend/apps/superapp/package.json frontend/apps/superapp/package-lock.json* ./
RUN npm ci

COPY frontend/apps/superapp/ .
RUN npx vite build

FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html
COPY frontend/apps/superapp/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 8080
USER nginx
CMD ["nginx", "-g", "daemon off;"]
