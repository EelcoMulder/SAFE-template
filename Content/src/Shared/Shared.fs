namespace Shared

#if (application == "counter")
type Counter = int
#else
type Message = string
#endif

#if (remoting)
module Route =
    /// Defines how routes are generated on server and mapped from client
    let builder typeName methodName =
        sprintf "/api/%s/%s" typeName methodName

/// A type that specifies the communication protocol between client and server
/// to learn more, read the docs at https://zaid-ajaj.github.io/Fable.Remoting/src/basics.html
#if (application == "counter")
type ICounterApi =
    { initialCounter : unit -> Async<Counter> }
#else
type IMessageApi =
    { initialMessage : unit -> Async<Message> }
#endif

#endif