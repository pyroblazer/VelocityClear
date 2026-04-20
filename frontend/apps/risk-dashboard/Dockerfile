FROM node:22-alpine AS build
WORKDIR /app

COPY frontend/apps/risk-dashboard/package.json frontend/apps/risk-dashboard/package-lock.json* ./
RUN npm ci

COPY frontend/apps/risk-dashboard/ .
RUN npx vite build

FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html
RUN echo 'server { listen 80; root /usr/share/nginx/html; index index.html; location / { try_files $uri $uri/ /index.html; } }' > /etc/nginx/conf.d/default.conf
USER nginx
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
