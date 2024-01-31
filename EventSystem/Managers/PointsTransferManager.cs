using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EventSystem.Managers
{
    public class PointsTransferManager
    {
        private Dictionary<string, PointsTransfer> _pendingTransfers = new Dictionary<string, PointsTransfer>();

        public async Task<string> InitiateTransfer(long senderSteamId, long points)
        {
            long? senderPoints = null;
            if (EventSystemMain.Instance.Config.UseDatabase)
            {
                senderPoints = await EventSystemMain.Instance.DatabaseManager.GetPlayerPointsAsync(senderSteamId);
            }
            else
            {
                var senderAccount = await EventSystemMain.Instance.PlayerAccountXmlManager.GetPlayerAccountAsync(senderSteamId);
                if (senderAccount != null)
                {
                    senderPoints = senderAccount.Points;
                }
            }

            if (senderPoints == null || senderPoints < points)
            {
                return null; // Gracz nie ma wystarczającej liczby punktów
            }

            var transferCode = Guid.NewGuid().ToString("N").Substring(0, 8);
            _pendingTransfers[transferCode] = new PointsTransfer
            {
                SenderSteamId = senderSteamId,
                Points = points,
                TransferCode = transferCode
            };
            return transferCode;
        }

        public async Task<(bool Success, long Points)> CompleteTransfer(string transferCode, long receiverSteamId)
        {
            if (!_pendingTransfers.TryGetValue(transferCode, out var transfer))
            {
                return (false, 0); // Kod transferowy nie istnieje
            }

            long? senderPoints = null;
            if (EventSystemMain.Instance.Config.UseDatabase)
            {
                senderPoints = await EventSystemMain.Instance.DatabaseManager.GetPlayerPointsAsync(transfer.SenderSteamId);
            }
            else
            {
                var senderAccount = await EventSystemMain.Instance.PlayerAccountXmlManager.GetPlayerAccountAsync(transfer.SenderSteamId);
                if (senderAccount != null)
                {
                    senderPoints = senderAccount.Points;
                }
            }

            if (senderPoints == null || senderPoints < transfer.Points)
            {
                _pendingTransfers.Remove(transferCode);
                return (false, 0); // Nadawca wydał punkty przed zakończeniem transferu
            }

            if (EventSystemMain.Instance.Config.UseDatabase)
            {
                var dbManager = EventSystemMain.Instance.DatabaseManager;
                var result1 = await dbManager.UpdatePlayerPointsAsync(transfer.SenderSteamId.ToString(), -transfer.Points);
                var result2 = await dbManager.UpdatePlayerPointsAsync(receiverSteamId.ToString(), transfer.Points);

                if (result1 && result2)
                {
                    _pendingTransfers.Remove(transferCode);
                    return (true, transfer.Points); // Transfer zakończony pomyślnie w bazie danych
                }
            }
            else
            {
                var xmlManager = EventSystemMain.Instance.PlayerAccountXmlManager;
                var result1 = await xmlManager.UpdatePlayerPointsAsync(transfer.SenderSteamId, -transfer.Points).ConfigureAwait(false);
                var result2 = await xmlManager.UpdatePlayerPointsAsync(receiverSteamId, transfer.Points).ConfigureAwait(false);

                if (result1 && result2)
                {
                    _pendingTransfers.Remove(transferCode);
                    return (true, transfer.Points); // Transfer zakończony pomyślnie w pliku XML
                }
            }

            return (false, 0); // Nie udało się zrealizować transferu
        }

    }

    public class PointsTransfer
    {
        public long SenderSteamId { get; set; }
        public long Points { get; set; }
        public string TransferCode { get; set; }
    }
}
