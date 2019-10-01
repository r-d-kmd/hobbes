FROM nginx

RUN apt-get update
RUN apt-get install -y openssl 

RUN mkdir /etc/nginx/ssl
RUN openssl req -subj '/CN=localhost' -nodes -x509 -newkey rsa:4096 -keyout /etc/nginx/ssl/nginx.key -out /etc/nginx/ssl/nginx.crt -days 365

COPY nginx.conf /etc/nginx/nginx.conf