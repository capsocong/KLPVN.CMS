﻿using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;

namespace KLPVN.Core.Helper;
public static class HttpContentHelper
{
  public static async Task<TObject?> DecodeAsync<TObject>(this HttpContent httpContent)
  {
    using var streamReader = new StreamReader(await httpContent.ReadAsStreamAsync());
    await using var jsonTextReader = new JsonTextReader(streamReader);
    var jsonSerializer = new JsonSerializer();
    return jsonSerializer.Deserialize<TObject>(jsonTextReader);
  }
  public static StringContent EncodeStringContent(this object obj, string contentType)
  {
    var json = JsonConvert.SerializeObject(obj); 
    var stringContent = new StringContent(json, Encoding.UTF8);
    stringContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
    return stringContent;
  }
  public static StringContent EncodeJsonContent(this object obj)
   => obj.EncodeStringContent("application/json");
}
