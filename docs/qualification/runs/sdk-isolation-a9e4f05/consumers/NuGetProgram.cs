using NPS.Core.Alpha20;
var offer = FeatureWireParser.ParseOffer("{}");
if (offer.Features.Count != 0 || offer.Required.Count != 0) throw new Exception("legacy parse");
try { FeatureWireParser.ParseOffer("{\"feature_negotiation\":1,\"feature_negotiation\":1}"); throw new Exception("accepted duplicate"); }
catch (FeatureFormatException error) when (error.Code == "NCP-FEATURE-NEGOTIATION-INVALID") { }
Console.WriteLine("11-package consumer built; public parser valid/duplicate cases passed");
