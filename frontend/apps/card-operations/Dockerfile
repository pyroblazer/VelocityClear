FROM node:22-alpine AS build
WORKDIR /app

COPY frontend/apps/card-operations/package.json frontend/apps/card-operations/package-lock.json* ./
RUN npm ci

COPY frontend/apps/card-operations/ .
RUN npx vite build

FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html
COPY frontend/apps/card-operations/nginx.conf /etc/nginx/conf.d/default.conf
USER nginx
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
