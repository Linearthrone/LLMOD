using HouseVictoria.Core.Models;

using HouseVictoria.Services.Trading;

using Xunit;



namespace HouseVictoria.Tests

{

    public class Mt4TradeBridgeHelperTests

    {

        [Fact]

        public void ParseExecutionResponse_SuccessWithTicket()

        {

            var result = Mt4TradeBridgeHelper.ParseExecutionResponse(

                "Trade_20260601120000_abc",

                "Trade executed successfully. Ticket: 12345678");



            Assert.True(result.Success);

            Assert.Equal(12345678, result.Ticket);

            Assert.Equal("Trade_20260601120000_abc", result.CommandId);

        }



        [Fact]

        public void ParseExecutionResponse_JsonSuccess()

        {

            var result = Mt4TradeBridgeHelper.ParseExecutionResponse(

                "Trade_json",

                """{"success":true,"ticket":42,"broker_symbol":"EURUSD.pro","message":"ok"}""");



            Assert.True(result.Success);

            Assert.Equal(42, result.Ticket);

            Assert.Equal("EURUSD.pro", result.BrokerSymbol);

        }



        [Fact]

        public void ParseExecutionResponse_FailureMessage()

        {

            var result = Mt4TradeBridgeHelper.ParseExecutionResponse(

                "Trade_test",

                "Trade execution failed. Error: 133");



            Assert.False(result.Success);

            Assert.Null(result.Ticket);

        }



        [Fact]

        public void ApplyTicketVerification_RejectsGhostExecution()

        {

            var parsed = Mt4TradeBridgeHelper.ParseExecutionResponse(

                "Trade_ghost",

                "Trade executed successfully. Ticket: 999");



            var verified = Mt4TradeBridgeHelper.ApplyTicketVerification(

                parsed,

                new List<Position>());



            Assert.False(verified.Success);

            Assert.False(verified.Verified);

            Assert.Contains("Ghost execution rejected", verified.Message);

        }



        [Fact]

        public void ApplyTicketVerification_AcceptsMatchingTicket()

        {

            var parsed = Mt4TradeBridgeHelper.ParseExecutionResponse(

                "Trade_ok",

                "Trade executed successfully. Ticket: 1001");



            var positions = new List<Position>

            {

                new() { Ticket = 1001, Symbol = "EURUSD.pro", Type = TradeType.Buy, Volume = 0.01 }

            };



            var verified = Mt4TradeBridgeHelper.ApplyTicketVerification(parsed, positions);



            Assert.True(verified.Success);

            Assert.True(verified.Verified);

            Assert.Equal("EURUSD.pro", verified.BrokerSymbol);

        }



        [Fact]

        public void TryParseTradeRequest_FromFencedBlock()

        {

            var note = """

                Strategy complete.



                ```trade

                {"Symbol":"EURUSD","Type":0,"Volume":0.01}

                ```

                """;



            var request = Mt4TradeBridgeHelper.TryParseTradeRequest(note);



            Assert.NotNull(request);

            Assert.Equal("EURUSD", request!.Symbol);

            Assert.Equal(0.01, request.Volume);

        }

    }

}


