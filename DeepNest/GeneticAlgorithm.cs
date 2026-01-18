using System;
using System.Collections.Generic;
using System.Linq;

namespace DeepNestLib
{
    public class GeneticAlgorithm
    {
        Random Rnd = new Random();

        public List<PopulationItem> Population;
        public GeneticAlgorithm(NestItem[] parts, NestConfig config)
        {
            Population = new List<PopulationItem>
            {
                NewPopulation(parts, true)
            };
            for (int s = 1; s < config.populationSize; s++)
            {
                Population.Add(NewPopulation(parts));
            }
        }
        public void NewGeneration(NestConfig config)
        {
            Population = Population.OrderBy(p => p.fitness).ToList();

            List<PopulationItem> newpopulation = new List<PopulationItem>(Population.Count);
            newpopulation.Add(Population[0]); // elitism

            while (newpopulation.Count < Population.Count)
            {
                PopulationItem male = RandomWeightedIndividual();
                PopulationItem female = RandomWeightedIndividual(male);

                PopulationItem[] children = Mate(male, female);

                for (int c = 0; c < children.Length && newpopulation.Count < Population.Count; c++)
                {
                    PopulationItem src = children[c];

                    PopulationItem clone = new PopulationItem
                    {
                        placements = new List<NFP>(src.placements),
                        Rotation = (float[])src.Rotation.Clone()
                    };

                    // apply mutation
                    Mutate(clone, config.MutationRate);
                    newpopulation.Add(clone);
                }
            }

            Population = newpopulation;
        }
        private PopulationItem NewPopulation(NestItem[] parts, bool first = false)
        {
            List<NFP> nfps = new List<NFP>();
            List<float> angles = new List<float>();
            var id = 0;
            for (int i = 0; i < parts.Count(); i++)
            {
                if (!parts[i].IsSheet)
                {
                    List<float> rotations = new List<float>();

                    switch (parts[i].Rots)
                    {
                        case EnabledRotations.NONE:
                            rotations = new List<float>
                                {
                                    0f
                                };
                            break;

                        case EnabledRotations.BY_180:
                            rotations = new List<float>
                                {
                                    0f,
                                    180f
                                };
                            break;

                        case EnabledRotations.BY_90:
                            rotations = new List<float>
                                {
                                    0f,
                                    90f,
                                    180f,
                                    270f
                                };
                            break;

                        case EnabledRotations.PAVE_0_90:
                            rotations = new List<float>
                                {
                                    0f,
                                    90f
                                };
                            break;

                        case EnabledRotations.ANY:
                            rotations = new List<float>
                                {
                                    parts[i].RotMinHeight,
                                    parts[i].RotMinHeight + 90f,
                                    parts[i].RotMinHeight + 180f,
                                    parts[i].RotMinHeight + 270f,
                                };
                            break;
                    }

                    int count = rotations.Count();
                    int quantity = parts[i].Quanity;

                    if (first)
                    {
                        float angle = rotations[0];
                        for (int j = 0; j < quantity; j++)
                        {
                            NFP nfp = Nest.CloneTree(parts[i].Polygon);
                            nfp.Id = id++;
                            nfp.source = i;
                            nfps.Add(nfp);
                            angles.Add(angle);
                        }
                        continue;
                    }

                    // 20% Chance to assign 100% of one rotation
                    if (Rnd.NextDouble() < 0.2)
                    {
                        float angle = rotations[Rnd.Next(count)];
                        for (int j = 0; j < quantity; j++)
                        {
                            NFP nfp = Nest.CloneTree(parts[i].Polygon);
                            nfp.Id = id++;
                            nfp.source = i;
                            nfps.Add(nfp);
                            angles.Add(angle);
                        }
                        continue;
                    }

                    // 20% to assign alternated rotations
                    if (Rnd.NextDouble() < 0.2)
                    {
                        float angle = rotations[Rnd.Next(count)];
                        for (int j = 0; j < quantity; j++)
                        {
                            NFP nfp = Nest.CloneTree(parts[i].Polygon);
                            nfp.Id = id++;
                            nfp.source = i;
                            nfps.Add(nfp);
                            if (j % 2 == 0)
                            {
                                angles.Add(angle);
                            }
                            else
                            {
                                if (angle >= 180f)
                                {
                                    angles.Add(angle - 180f);
                                }
                                else
                                {
                                    angles.Add(angle + 180f);
                                }
                            }
                        }
                        continue;
                    }

                    // Otherwise, distribute randomly across available rotations
                    // Generate random weights
                    double[] weights = new double[count];
                    double total = 0;
                    for (int r = 0; r < count; r++)
                    {
                        weights[r] = Rnd.NextDouble();
                        total += weights[r];
                    }

                    // Convert to percentages
                    for (int r = 0; r < count; r++)
                        weights[r] /= total;

                    // Compute how many pieces per rotation
                    int[] counts = new int[count];
                    int assigned = 0;
                    for (int r = 0; r < count; r++)
                    {
                        counts[r] = (int)Math.Round(weights[r] * quantity);
                        assigned += counts[r];
                    }

                    // Adjust rounding errors
                    while (assigned < quantity)
                    {
                        counts[Rnd.Next(count)]++;
                        assigned++;
                    }
                    while (assigned > quantity)
                    {
                        int idx = Rnd.Next(count);
                        if (counts[idx] > 0)
                        {
                            counts[idx]--;
                            assigned--;
                        }
                    }

                    // Assign angles based on counts
                    for (int r = 0; r < count; r++)
                    {
                        for (int j = 0; j < counts[r]; j++)
                        {
                            NFP nfp = Nest.CloneTree(parts[i].Polygon);
                            nfp.Id = id++;
                            nfp.source = i;
                            nfps.Add(nfp);
                            angles.Add(rotations[r]);
                        }
                    }
                }
            }

            var paired = nfps
                .Zip(angles, (poly, ang) => new { poly, ang })
                .OrderByDescending(x => Math.Abs(GeometryUtil.polygonArea(x.poly)))
                .ToList();
            nfps = paired.Select(x => x.poly).ToList();
            angles = paired.Select(x => x.ang).ToList();

            return new PopulationItem
            {
                placements = nfps,
                Rotation = angles.ToArray(),
            };
        }
        private PopulationItem RandomWeightedIndividual(PopulationItem exclude = null)
        {
            List<PopulationItem> candidates = Population.Where(p => p != exclude).ToList();
            if (candidates.Count == 0)
            {
                return Population[Rnd.Next(Population.Count)];
            }

            // Compute inverted weights: lower fitness → higher weight
            double maxFitness = (double)candidates.Max(p => p.fitness);
            double totalWeight = 0;
            double[] weights = new double[candidates.Count];
            for (int i = 0; i < candidates.Count; i++)
            {
                // +1e-6 to avoid division by zero when all fitness are equal
                weights[i] = (double)((maxFitness - candidates[i].fitness) + 1e-6);
                totalWeight += weights[i];
            }

            double pick = Rnd.NextDouble() * totalWeight;
            double cumulative = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                cumulative += weights[i];
                if (pick <= cumulative)
                    return candidates[i];
            }

            return candidates.Last();
        }
        private PopulationItem[] Mate(PopulationItem male, PopulationItem female)
        {
            int count = male.placements.Count;
            int cutpoint = (int)Math.Round(Math.Min(Math.Max(Rnd.NextDouble(), 0.1), 0.9) * (count - 1));

            var gene1 = new List<NFP>(male.placements.Take(cutpoint));
            var rot1 = new List<float>(male.Rotation.Take(cutpoint));
            var ids1 = new HashSet<int>(gene1.Select(g => g.Id));

            var gene2 = new List<NFP>(female.placements.Take(cutpoint));
            var rot2 = new List<float>(female.Rotation.Take(cutpoint));
            var ids2 = new HashSet<int>(gene2.Select(g => g.Id));

            for (int i = 0; i < female.placements.Count; i++)
            {
                var f = female.placements[i];
                if (ids1.Add(f.Id))
                {
                    gene1.Add(f);
                    rot1.Add(female.Rotation[i]);
                }
            }

            for (int i = 0; i < male.placements.Count; i++)
            {
                var m = male.placements[i];
                if (ids2.Add(m.Id))
                {
                    gene2.Add(m);
                    rot2.Add(male.Rotation[i]);
                }
            }

            return new[]
            {
                new PopulationItem { placements = gene1, Rotation = rot1.ToArray() },
                new PopulationItem { placements = gene2, Rotation = rot2.ToArray() }
            };
        }
        private void Mutate(PopulationItem item, double mutationRate)
        {
            if (Rnd.NextDouble() > mutationRate) return;

            int n = item.placements.Count;
            if (n < 2) return;

            // pick adjacent index
            int i = Rnd.Next(n - 1);

            // swap placements
            var tmp = item.placements[i];
            item.placements[i] = item.placements[i + 1];
            item.placements[i + 1] = tmp;

            // swap rotations
            float tmpR = item.Rotation[i];
            item.Rotation[i] = item.Rotation[i + 1];
            item.Rotation[i + 1] = tmpR;
        }
    }
}