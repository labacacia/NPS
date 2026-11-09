import com.fasterxml.jackson.databind.*;
import com.labacacia.nps.alpha20.FeatureWireParser;
import java.nio.file.*;
import java.util.*;
public class Consumer {
 public static void main(String[] args) throws Exception {
  var mapper = new ObjectMapper(); var cases = mapper.readTree(Files.readString(Path.of(args[0]))).get("cases");
  if (cases.size() != 44) throw new AssertionError("case inventory");
  for (var vector : cases) {
   var error = vector.get("expected").path("error").asText();
   boolean nativeOffer = vector.get("kind").asText().equals("native-offer");
   var headers = new ArrayList<String>(); vector.path("headers").forEach(value -> headers.add(value.asText()));
   boolean invalid = error.equals(FeatureWireParser.NATIVE_INVALID) || error.equals(FeatureWireParser.HTTP_INVALID);
   try {
    if (nativeOffer) {
     var actual = FeatureWireParser.parseOffer(vector.get("raw").asText());
     var input = mapper.readTree(vector.get("raw").asText());
     if (input.path("feature_offer").size() != actual.features().size()) throw new AssertionError("offer size");
     for (var item : input.path("feature_offer"))
      if (!item.get("revisions").equals(mapper.valueToTree(actual.features().get(item.get("id").asText())))) throw new AssertionError("revisions");
     var required = new ArrayList<String>(); input.path("required_features").forEach(value -> required.add(value.asText()));
     if (!required.equals(actual.required())) throw new AssertionError("required");
    } else if (!vector.get("expected").get("required").equals(mapper.valueToTree(FeatureWireParser.parseRequiredHeader(headers)))) throw new AssertionError("header");
    if (invalid) throw new AssertionError("accepted invalid");
   } catch (FeatureWireParser.FormatException exception) {
    if (!invalid || !exception.code().equals(error)) throw exception;
   }
  }
  System.out.println("{\"parser_cases\":44,\"actual_jar_consumer\":true,\"passed\":true}");
 }
}
