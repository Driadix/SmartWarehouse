package main

import (
	"encoding/json"
	"log"
	"net/http"
	"os"
)

type healthResponse struct {
	Service string `json:"service"`
	Status  string `json:"status"`
}

func main() {
	port := readEnv("SIMULATION_HOST_PORT", "8080")

	mux := http.NewServeMux()
	mux.HandleFunc("/", writeHealth("simulation-host"))
	mux.HandleFunc("/health/live", writeHealth("simulation-host"))
	mux.HandleFunc("/health/ready", writeHealth("simulation-host"))

	server := &http.Server{
		Addr:    ":" + port,
		Handler: mux,
	}

	log.Printf("simulation-host listening on :%s", port)

	if err := server.ListenAndServe(); err != nil && err != http.ErrServerClosed {
		log.Fatalf("simulation-host exited with error: %v", err)
	}
}

func writeHealth(service string) http.HandlerFunc {
	return func(writer http.ResponseWriter, request *http.Request) {
		writer.Header().Set("Content-Type", "application/json; charset=utf-8")
		_ = json.NewEncoder(writer).Encode(healthResponse{
			Service: service,
			Status:  "healthy",
		})
	}
}

func readEnv(key string, fallback string) string {
	if value := os.Getenv(key); value != "" {
		return value
	}

	return fallback
}
