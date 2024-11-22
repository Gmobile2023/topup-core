using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Text;

namespace HLS.Paygate.Shared.Utils
{
    public class SequenceGenerator
    {
        //private static int _unusedBits = 1; // Sign bit, Unused (always set to 0)
        //private static int _epochBits = 41;
        private const int NodeIdBits = 10;
        private const int SequenceBits = 12;

        private static readonly int MaxNodeId = (int)(Math.Pow(2, NodeIdBits) - 1);
        private static readonly int MaxSequence = (int)(Math.Pow(2, SequenceBits) - 1);

        // Custom Epoch (January 1, 2015 Midnight UTC = 2015-01-01T00:00:00Z)
        private const long CustomEpoch = 1420070400000L;

        private readonly int _nodeId;

        private long _lastTimestamp = -1L;
        private long _sequence = 0L;

        // Create SequenceGenerator with a nodeId
        public SequenceGenerator(int nodeId) {
            if(nodeId < 0 || nodeId > MaxNodeId) {

            }
            _nodeId = nodeId;
        }

        // Let SequenceGenerator generate a nodeId
        public SequenceGenerator() {
            _nodeId = CreateNodeId();
        }

        public long GetSequenceId() {
            long currentTimestamp = Timestamp();

            if(currentTimestamp < _lastTimestamp) {
                throw new Exception();
            }

            if (currentTimestamp == _lastTimestamp) {
                _sequence = (_sequence + 1) & MaxSequence;
                if(_sequence == 0) {
                    // Sequence Exhausted, wait till next millisecond.
                    currentTimestamp = WaitNextMillis(currentTimestamp);
                }
            } else {
                // reset sequence to start with zero for the next millisecond
                _sequence = 0;
            }

            _lastTimestamp = currentTimestamp;

            var id = currentTimestamp << (NodeIdBits + SequenceBits);
            id |= _nodeId << SequenceBits;
            id |= _sequence;
            return id;
        }

        private static long Timestamp() {
            //return Instant.now().toEpochMilli() - CustomEpoch;
            //return DateTime.Now.Millisecond - CustomEpoch;
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - CustomEpoch;
        }
        private long WaitNextMillis(long currentTimestamp) {
            while (currentTimestamp == _lastTimestamp) {
                currentTimestamp = Timestamp();
            }
            return currentTimestamp;
        }
        private int CreateNodeId() {
            int nodeId;
            try {
                var sb = new StringBuilder();
                // var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
                // while (networkInterfaces.hasMoreElements()) {
                //     NetworkInterface networkInterface = networkInterfaces.nextElement();
                //     byte[] mac = networkInterface.getHardwareAddress();
                //     if (mac != null) {
                //         for(int i = 0; i < mac.Length; i++) {
                //             sb.Append(String.Format("%02X", mac[i]));
                //         }
                //     }
                // }
                foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
                {

                    if (networkInterface.GetPhysicalAddress().ToString() != "")
                    {
                        sb.Append($"{networkInterface.GetPhysicalAddress():02X}");
                    }
                }
                nodeId = sb.ToString().GetHashCode();
            } catch (Exception) {
                nodeId = new Random().Next();
            }
            nodeId = nodeId & MaxNodeId;
            return nodeId;
        }
    }
}
