FROM builder AS build
ARG EXECUTABLE
ENV EXECUTABLE=${EXECUTABLE}

# final stage/image
FROM kmdrd/runtime
COPY --from=build /tmp/start.sh /tmp/start.sh 
WORKDIR /app
COPY --from=build /app .

ENV COLLECTION test
ENV HOST "http://gateway-svc"
ENV PAT ""
ENV port 8085

ENTRYPOINT /tmp/start.sh