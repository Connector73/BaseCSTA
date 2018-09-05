# BaseCSTA

Zultys MXvirtual provides a Computer Supported Telecommunications Applications (CSTA) interface for third-party developers
that allows software applications to communicate with the MX system. The interface connection is a server-based (3rd party)
based connection. The goal of providing the CSTA interface is to allow software applications to interact with the MX system
to create useful Communication Enabled Business Processes. The Zultys CSTA interface conforms to ECMA standards 269 and 232
for call control events. Please note that not all events are supported by the Zultys CSTA interface.

This project is designed to provide access for basic functionality of CSTA interface.

## History

Following the successful implementation of a CSTA client library in C# for experimental use, this will be an attempt to create
a more complete, more universal and more robust implementation. There are several pitfalls when working with MXvirtual as a 
"Zultys Proprietary Function". The ECMA standard is most suitable for circuit switched telephony.

This project is still in its very beginnings but given the already working (crudely done, highly experimental, incomplete and
unstable) implementation progress should be visible and hopefully usable soon.

## Project Goals

The main goal of this project is to be able to control MXvirtual systems via a CTI Application. Feel free to leave a note as
an issue if you encounter problems using this client library and later in the application development process.
MXvitual must stay untouched! No patches or changes should be necessary to use this implementation.

## Roadmap

1. Create a usable object model (based on ECMA and Zultys extensions) to represent an MXvirtual as CSTA server
2. Make the principal objects serializable for use as CSTA-XML Events/Requests/Responses
3. Handle TCP server connections, establish and keep-alive CSTA sessions
4. Generate suitable CSTA events for all conditions
5. Process CSTA requests and control MXvirtual accordingly
6. Provide support for Zultys CSTA extended functions

## Test account

All units tests have a preconfigured test account. This account is limited and don't provide support for many instances.
Please don't hesitate to ask us for additional test servers and accounts via email: support@connector73.com.

## Contributing

Anybody may file an issue or send pull requests. Any help is greatly appreciated.
