package main
import (
 "fmt"
 "github.com/labacacia/NPS-sdk-go/alpha20"
)
func main() {
 offer, err := alpha20.ParseFeatureOffer("{}")
 if err != nil || len(offer.Features()) != 0 { panic("legacy offer") }
 _, err = alpha20.ParseFeatureOffer("{\"feature_negotiation\":1,\"feature_negotiation\":1}")
 if err == nil { panic("accepted duplicate") }
 fmt.Println("downloaded module zip consumer: public parser valid/duplicate cases passed")
}
