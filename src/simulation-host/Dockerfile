# syntax=docker/dockerfile:1.7

FROM golang:1.25.1-alpine AS build
WORKDIR /src

COPY . .
RUN go build -o /app/simulation-host ./cmd/simulation-host

FROM alpine:3.22
WORKDIR /app

RUN adduser -D -u 10001 appuser

COPY --from=build /app/simulation-host /app/simulation-host

USER appuser
EXPOSE 8080

ENTRYPOINT ["/app/simulation-host"]
