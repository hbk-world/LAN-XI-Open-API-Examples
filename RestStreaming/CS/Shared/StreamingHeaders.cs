using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Shared
{
    /// <summary>
    /// Header for every message received from the LAN-XI modules through the streaming socket
    /// </summary>
    public struct StreamingHeader
    {
        public Byte[] magic;
        public UInt16 headerLength; //!< Length of the header.
        public UInt16 messageType; //!< The type of the message following the header. @see kSignalDataMessage, @see kDataQuelityMessage and @see kInterpretationMessage.
        public Int16 reserved1;
        public Int32 reserved2;
        public UInt32 timestampFamily; //!< Indicates the conversion between time and samples (1/(2^n)).
        public UInt64 timestamp; //!< Timestamp indicating the timing of the message.
        public UInt32 dataLength; //!< The number of bytes in the message. Used to tell how much should be read when fetching the message.
    }

    /// <summary>
    /// Header for every signal data message received from the LAN-XI modules. Follows the StreamingHeader.
    /// </summary>
    public struct SignalDataMessage
    {
        public UInt16 numberOfSignals; //!< The number of signals transmitted.
        public Int16  reserved1;
        public UInt16 signalId; //!< Signal identifier
        public UInt16 numberOfValues; //!< Number of values transmitted in the message.
    }

    /// <summary>
    /// Header for every signal data message received from the LAN-XI modules. Follows the StreamingHeader.
    /// </summary>
    public struct ScaleInterpretationMessage
    {
        public UInt16 signalId; //!< Signal identifier
        public UInt16 DescriptorType; //!< Identifies the descriptor.
        public Int16  reserved1;
        public UInt16 ValueLength; //!< Length of value in bytes, not including any padding that may have been added to Value to make it a multiple of 32-bit word
        public double Scale;
    }

    /// <summary>
    /// WebXI AuxSequenceData header used for CAN data. Follows WebXI header.
    /// </summary>
    public struct AuxSequenceDataHeader
    {
        public UInt16 numberOfSequence;
        public UInt16 reserved;
        public UInt16 sequenceId;
        public UInt16 numberOfValues;
    }


    /// <summary>
    /// WebXI AuxSequenceData used to transmit CAN data. Follows WebXI header.
    /// </summary>
    public struct AuxSequenceData
    {
        public UInt32 relativeOffsetTime;
        public Byte   status;
        public Byte   canMessageInfo;
        public Byte   canDataSize;
        public Byte   reserved;
        public UInt32 canMessageID;
        public byte[] canData;          // 8 bytes of CAN data
    }

}
