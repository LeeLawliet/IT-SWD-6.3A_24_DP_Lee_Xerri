# Use nginx image
FROM nginx:alpine

# Copy the custom nginx config
COPY nginx.conf /etc/nginx/nginx.conf

# Copy published Blazor WASM files
COPY ./publish/wwwroot /usr/share/nginx/html

# Expose Cloud Run default port
EXPOSE 8080

# Run nginx
CMD ["nginx", "-g", "daemon off;"]
