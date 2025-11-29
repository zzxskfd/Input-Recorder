/// <summary>
/// Controls a simple Rock-Paper-Scissors (RPS) game within a Unity scene.
/// </summary>
/// <remarks>
/// - Maps player input to moves using keys: A = Rock, S = Paper, D = Scissors.
/// - Maintains basic match statistics (rounds, player wins, AI wins, draws).
/// - Uses an optional <c>InputRecorder</c> instance (if present) to predict the player's next move
///   and choose an AI response. If no recorder is available, falls back to a uniform random strategy.
/// - Intended to be attached to a GameObject and given a reference to a UI <c>Text</c> component
///   for displaying round results and statistics.
/// </remarks>

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Amaz1ngGames.InputRecorder
{
    public class RPSGameController : MonoBehaviour
    {
        // Reference to the UI Text component
        public Text displayTextField;

        // Statistics
        private int roundNum = 0;
        private int playerWins = 0;
        private int aiWins = 0;
        private int drawCount = 0;

        // Define the Move types
        private enum Move { Rock, Paper, Scissors }

        // AI move
        private Move nextMove;
        private InputRecorder inputRecorder;

        void Awake()
        {
            if (!InputRecorder.InstanceExists)
            {
                Debug.LogWarning("InputRecorder not found, will use basic random strategy.");
            }
            else
            {
                inputRecorder = InputRecorder.Instance;
                inputRecorder.backend = InputRecorder.InputBackend.OldInput;
                inputRecorder.StartRecording();
            }
        }

        void Update()
        {
            // specific mapping: A=Rock, S=Paper, D=Scissors
            if (Input.GetKeyDown(KeyCode.A))
            {
                ProcessRound(Move.Rock);
            }
            else if (Input.GetKeyDown(KeyCode.S))
            {
                ProcessRound(Move.Paper);
            }
            else if (Input.GetKeyDown(KeyCode.D))
            {
                ProcessRound(Move.Scissors);
            }

            // Update next move, preventing player's move leak
            nextMove = GetAINextMove();
        }

        #region AI FUNCTION
        // This is the function you can modify to test your AI.
        // By default, it returns a random move.
        private Move GetAINextMove()
        {
            if (inputRecorder != null)
            {
                // Predict the player's choice to be 
                var stats = inputRecorder.GetStatsSnapshot();
                var counts = new List<int>
                {
                    stats.oldKeyCounts.GetValueOrDefault(KeyCode.A, 0),
                    stats.oldKeyCounts.GetValueOrDefault(KeyCode.S, 0),
                    stats.oldKeyCounts.GetValueOrDefault(KeyCode.D, 0),
                };
                int index = GetRandomIndexOfMaxValue(counts);
                if (index == 0)
                    return Move.Paper;
                else if (index == 1)
                    return Move.Scissors;
                else
                    return Move.Rock;
            }
            else
            {
                int randomIndex = Random.Range(0, 3); // Returns 0, 1, or 2
                return (Move)randomIndex;
            }
        }

        private static int GetRandomIndexOfMaxValue(List<int> list)
        {
            if (list == null || !list.Any())
            {
                Debug.LogError("The list cannot be null or empty.");
            }

            // 1. Find the maximum value
            int maxValue = list.Max();

            // 2. Collect indices of maximum value
            List<int> maxIndices = new List<int>();
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == maxValue)
                {
                    maxIndices.Add(i);
                }
            }

            // 3. Select a random index
            int randomIndexInMaxIndices = Random.Range(0, maxIndices.Count);
            return maxIndices[randomIndexInMaxIndices];
        }
        #endregion

        private void ProcessRound(Move playerMove)
        {
            // 1. Get AI Choice
            Move aiMove = nextMove;

            // 2. Determine Winner
            string resultString;
            
            if (playerMove == aiMove)
            {
                resultString = "It's a Draw!";
                drawCount++;
            }
            else if ((playerMove == Move.Rock && aiMove == Move.Scissors) ||
                    (playerMove == Move.Paper && aiMove == Move.Rock) ||
                    (playerMove == Move.Scissors && aiMove == Move.Paper))
            {
                resultString = "Player Wins!";
                playerWins++;
            }
            else
            {
                resultString = "AI Wins!";
                aiWins++;
            }

            // 3. Update UI
            UpdateUI(playerMove, aiMove, resultString);

            roundNum++;
        }

        private void UpdateUI(Move pMove, Move aMove, string result)
        {
            string log = "A=Rock, S=Paper, D=Scissors\n\n"
                        + $"------------------\n"
                        + $"Round {roundNum}\n"
                        + $"Player chose: {pMove}\n"
                        + $"AI chose: {aMove}\n"
                        + $"<b>Result: {result}</b>\n\n"
                        + $"------------------\n"
                        + $"Stats:\n"
                        + $"Player Wins: {playerWins} | AI Wins: {aiWins} | Draws: {drawCount}";

            displayTextField.text = log;
        }
    }

}