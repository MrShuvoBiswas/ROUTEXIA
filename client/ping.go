package main

import (
	"fmt"
	"net"
	"time"
)

func main() {
	addr, _ := net.ResolveUDPAddr("udp", "3.1.31.201:9001")
	conn, err := net.DialUDP("udp", nil, addr)
	if err != nil {
		fmt.Println("Dial Error:", err)
		return
	}
	conn.SetDeadline(time.Now().Add(3 * time.Second))
	
	// RXIA Ping Probe
	_, err = conn.Write([]byte{0x52, 0x58, 0x49, 0x41, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00})
	if err != nil {
		fmt.Println("Write Error:", err)
		return
	}
	
	buf := make([]byte, 1024)
	n, _, err := conn.ReadFromUDP(buf)
	if err != nil {
		fmt.Println("Read Error:", err)
	} else {
		fmt.Printf("Success! Received %d bytes\n", n)
	}
}
