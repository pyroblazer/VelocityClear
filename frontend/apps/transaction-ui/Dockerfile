FROM node:22-alpine AS build
WORKDIR /app

COPY frontend/apps/transaction-ui/package.json frontend/apps/transaction-ui/package-lock.json* ./
RUN npm ci

COPY frontend/apps/transaction-ui/ .
RUN npx vite build

FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html
COPY frontend/apps/transaction-ui/nginx.conf /etc/nginx/conf.d/default.conf
USER nginx
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
