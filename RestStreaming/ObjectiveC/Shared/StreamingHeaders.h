//
//  StreamingHeaders.h
//  LANXISimpleStreamingExamples
//
//  Headers etc. used for input streaming

#ifndef LANXISimpleStreamingExamples_StreamingHeaders_h
#define LANXISimpleStreamingExamples_StreamingHeaders_h

// Header for every message received from the LAN-XI module through the streaming socket
typedef struct __attribute__((packed))
{
    UInt8  magic[2];
    UInt16 headerLength; //!< Length of the header.
    UInt16 messageType; //!< The type of the message following the header. @see kSignalDataMessage, @see kDataQualityMessage and @see kInterpretationMessage.
    SInt16 reserved1;
    SInt32 reserved2;
    UInt32 timestampFamily; //!< Indicates the length of each tick in the timestamp. For a timestampFamily value of N, each tick of timestamp would have the duration 1/(2^N) seconds
    UInt64 timestamp; //!< Indicates the timing of the first sample in the following message
    UInt32 dataLength; //!< The number of bytes in the message. Used to tell how much should be read when fetching the message.
} StreamingHeader;

// Header for every signal data message received from the LAN-XI module. Follows the StreamingHeader.
typedef struct __attribute__((packed))
{
    UInt16 numberOfSignals; //!< The number of signals transmitted.
    SInt16 reserved1;
    UInt16 signalId; //!< Signal identifier.
    UInt16 numberOfValues; //!< Number of values transmitted in the message.
} SignalDataMessage;

#endif
