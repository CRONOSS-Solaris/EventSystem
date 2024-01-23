using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EventSystem.Managers
{
    public class PointsTransferManager
    {
        private Dictionary<string, PointsTransfer> _pendingTransfers = new Dictionary<string, PointsTransfer>();

        public string InitiateTransfer(long senderSteamId, long points)
        {
            // Sprawdzenie, czy gracz ma wystarczającą liczbę punktów
            var senderAccount = EventSystemMain.Instance.PlayerAccountXmlManager.GetPlayerAccountAsync(senderSteamId).Result;
            if (senderAccount == null || senderAccount.Points < points)
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

            // Ponowne sprawdzenie punktów nadawcy
            var senderAccount = await EventSystemMain.Instance.PlayerAccountXmlManager.GetPlayerAccountAsync(transfer.SenderSteamId);
            if (senderAccount == null || senderAccount.Points < transfer.Points)
            {
                _pendingTransfers.Remove(transferCode);
                return (false, 0); // Nadawca wydał punkty przed zakończeniem transferu
            }

            // Proces transferu punktów
            if (EventSystemMain.Instance.Config.UseDatabase)
            {
                var dbManager = EventSystemMain.Instance.DatabaseManager;
                if (dbManager.UpdatePlayerPoints(transfer.SenderSteamId.ToString(), -transfer.Points) &&
                    dbManager.UpdatePlayerPoints(receiverSteamId.ToString(), transfer.Points))
                {
                    _pendingTransfers.Remove(transferCode);
                    return (true, transfer.Points); // Transfer zakończony pomyślnie w bazie danych
                }
            }
            else
            {
                var xmlManager = EventSystemMain.Instance.PlayerAccountXmlManager;
                if (await xmlManager.UpdatePlayerPointsAsync(transfer.SenderSteamId, -transfer.Points).ConfigureAwait(false) &&
                    await xmlManager.UpdatePlayerPointsAsync(receiverSteamId, transfer.Points).ConfigureAwait(false))
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
