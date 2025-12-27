FROM node:20-alpine

WORKDIR /app

# Install dependencies
COPY package*.json ./
RUN npm install

# Copy server code
COPY . .

# Expose the API port
EXPOSE 3000

CMD ["node", "server.js"]
