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

                {"Symbol":"EURUSD","Type":0,"Volume":0.01,"StopLoss":1.0830}

                ```

                """;



            var request = Mt4TradeBridgeHelper.TryParseTradeRequest(note);



            Assert.NotNull(request);

            Assert.Equal("EURUSD", request!.Symbol);

            Assert.Equal(0.01, request.Volume);

            Assert.Equal(1.0830, request.StopLoss);

        }



        [Fact]

        public void TryParseTradeRequest_FromRawJsonDetail()

        {

            var detail = """{"Symbol":"GBPUSD","Type":1,"Volume":0.01,"StopLoss":1.2750}""";

            var request = Mt4TradeBridgeHelper.TryParseTradeRequest(detail);



            Assert.NotNull(request);

            Assert.Equal(TradeType.Sell, request!.Type);

            Assert.Equal(1.2750, request.StopLoss);

        }



        [Fact]

        public void ApplyDefaultStopLoss_FillsMissingStopForBuy()

        {

            var request = new TradeRequest

            {

                Symbol = "EURUSD",

                Type = TradeType.Buy,

                Volume = 0.01

            };

            var quote = new MarketData { Symbol = "EURUSD", Bid = 1.08500, Ask = 1.08520 };



            Mt4TradeBridgeHelper.ApplyDefaultStopLoss(request, quote);



            Assert.NotNull(request.StopLoss);

            Assert.True(request.StopLoss < quote.Bid);

        }



        [Fact]

        public void SanitizeStopLossAndTakeProfit_CorrectsStaleStopLoss()

        {

            var request = new TradeRequest

            {

                Symbol = "EURUSD",

                Type = TradeType.Buy,

                Volume = 0.01,

                StopLoss = 1.0830,

                TakeProfit = 1.0900

            };

            var quote = new MarketData { Symbol = "EURUSD", Bid = 1.17000, Ask = 1.17020 };



            var note = Mt4TradeBridgeHelper.SanitizeStopLossAndTakeProfit(request, quote);



            Assert.NotNull(note);

            Assert.Contains("StopLoss corrected", note);

            Assert.NotNull(request.StopLoss);

            Assert.True(request.StopLoss < quote.Bid);

            Assert.True(Math.Abs(quote.Bid - request.StopLoss!.Value - 0.0020) < 0.00005);

            Assert.Null(request.TakeProfit);

        }



        [Fact]

        public void SanitizeStopLossAndTakeProfit_KeepsValidStopLoss()

        {

            var request = new TradeRequest

            {

                Symbol = "EURUSD",

                Type = TradeType.Buy,

                Volume = 0.01,

                StopLoss = 1.0830

            };

            var quote = new MarketData { Symbol = "EURUSD", Bid = 1.08500, Ask = 1.08520 };



            var note = Mt4TradeBridgeHelper.SanitizeStopLossAndTakeProfit(request, quote);



            Assert.Null(note);

            Assert.Equal(1.0830, request.StopLoss);

        }



        [Fact]

        public void SanitizeStopLossAndTakeProfit_CorrectsPipCountMistakenForPrice()

        {

            var request = new TradeRequest

            {

                Symbol = "EURUSD",

                Type = TradeType.Buy,

                Volume = 0.01,

                StopLoss = 20

            };

            var quote = new MarketData { Symbol = "EURUSD", Bid = 1.08500, Ask = 1.08520 };



            var note = Mt4TradeBridgeHelper.SanitizeStopLossAndTakeProfit(request, quote);



            Assert.NotNull(note);

            Assert.True(request.StopLoss < quote.Bid);

        }



        [Fact]

        public void TryPrepareTradeForExecution_RejectsWhenQuoteUnavailable()

        {

            var request = new TradeRequest

            {

                Symbol = "EURUSD",

                Type = TradeType.Buy,

                Volume = 0.01,

                StopLoss = 1.0830

            };



            var ok = Mt4TradeBridgeHelper.TryPrepareTradeForExecution(request, null, out var corrections, out var error);



            Assert.False(ok);

            Assert.Null(corrections);

            Assert.Contains("Live quote unavailable", error);

            Assert.Equal(1.0830, request.StopLoss);

        }



        [Fact]

        public void TryPrepareTradeForExecution_CorrectsStaleStopBeforeSend()

        {

            var request = new TradeRequest

            {

                Symbol = "EURUSD",

                Type = TradeType.Buy,

                Volume = 0.01,

                StopLoss = 1.0780,

                TakeProfit = 1.0920

            };

            var quote = new MarketData { Symbol = "EURUSD", Bid = 1.15900, Ask = 1.15920 };



            var ok = Mt4TradeBridgeHelper.TryPrepareTradeForExecution(request, quote, out var corrections, out var error);



            Assert.True(ok);

            Assert.Empty(error);

            Assert.NotNull(corrections);

            Assert.Contains("StopLoss corrected", corrections);

            Assert.True(request.StopLoss < quote.Bid);

            Assert.Null(request.TakeProfit);

        }

    }

}


