namespace C2pa
{

    public static class ExceptionFactory {
        public static Exception GetException(string type, string message) {
            return type switch {
                "C2paException" => new C2paException(message),
                _ => new C2paException(message)
            };
        }
    }

    [Serializable]
    public class C2paException(string message) : Exception(message) { }
}