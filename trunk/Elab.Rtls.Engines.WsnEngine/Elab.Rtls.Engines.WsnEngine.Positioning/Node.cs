﻿namespace Elab.Rtls.Engines.WsnEngine.Positioning
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Text;

    using DatabaseConnection;

    /// <summary>
    /// Struct that holds information about the anchors, including RSS!
    /// </summary>
    public class AnchorNode
    {
        #region Fields

        public Queue<double> RSS = new Queue<double>(20);
        public double fRSS;
        public string nodeid;
        public double posx;
        public double posy;
        public double range;
        public DateTime lastUpdate;

        #endregion Fields

        #region Constructors

        public AnchorNode(string wsnid, double posx, double posy, double RSS, DateTime now)
        {
            this.nodeid = wsnid;
            this.posx = posx;
            this.posy = posy;
            this.RSS.Enqueue(RSS);
            this.lastUpdate = now;
        }

        #endregion Constructors
    }

    //: Elab.Rtls.Engines.WsnEngine.Positioning.INode
    public class Node
    {
        #region Fields

        private MySQLClass MyDb;
        private string WsnId;
        private List<AnchorNode> anchorList = new List<AnchorNode>();
        private Point position;
        private List<AnchorNode> virtualAnchorList = new List<AnchorNode>();

        #endregion Fields

        #region Constructors

        public Node(string WsnId, MySQLClass MyDb)
        {
            this.MyDb = MyDb;
            this.WsnId = WsnId;
        }

        //public Node(string WsnId, MySQLClass MyDb, string AnchorWsnId, double RSS, int van, DateTime now)
        //{
        //    this.MyDb = MyDb;
        //    this.WsnId = WsnId;
        //    NewAnchor(AnchorWsnId, RSS, van, now);
        //}

        #endregion Constructors

        #region Delegates

        public delegate double FilterMethod(Queue<double> RSS);

        public delegate double RangingMethod(double fRSS);

        #endregion Delegates

        #region Properties

        public List<AnchorNode> Anchors
        {
            get { return anchorList; }
        }

        public Point Position
        {
            get
                {
                    return position;
                }
        }

        public List<AnchorNode> VirtualAnchors
        {
            get { return virtualAnchorList; }
        }

        public string WsnIdProperty
        {
            get { return WsnId; }
        }

        #endregion Properties

        #region Methods

        public void UpdateAnchors(string AnchorWsnId, double RSS, int van, DateTime now)
        {
            Point ANpos = new Point();
            AnchorNode TempNode;
            double fRSS;

            ANpos = GetANPosition(AnchorWsnId);

            if (anchorList.Exists(AN => AN.nodeid == AnchorWsnId) || virtualAnchorList.Exists(VAN => VAN.nodeid == AnchorWsnId))
            {
                if (van == 1)
                {
                    int index = anchorList.FindIndex(AN => AN.nodeid == AnchorWsnId);

                    anchorList[index].RSS.Enqueue(RSS);

                    if (anchorList[index].RSS.Count > 20)
                    {
                        do
                        {
                            anchorList[index].RSS.Dequeue();
                        } while (anchorList[index].RSS.Count > 20);
                    }

                    anchorList[index].lastUpdate = now;
                }
                else
                {
                    int index = virtualAnchorList.FindIndex(AN => AN.nodeid == AnchorWsnId);

                    virtualAnchorList[index].RSS.Enqueue(RSS);

                    if (virtualAnchorList[index].RSS.Count > 20)
                    {
                        do
                        {
                            virtualAnchorList[index].RSS.Dequeue();
                        } while (virtualAnchorList[index].RSS.Count > 20);
                    }

                    virtualAnchorList[index].lastUpdate = now;
                }
            }
            else
                NewAnchor(AnchorWsnId, RSS, van, now);

            RemoveOutdatedAnchors();
            UpdateAnchorPositions();
        }

        /// <summary>
        /// Allows a node to determine its own position from the database, 
        /// only applicable in case the node is an anchor
        /// </summary>
        public void SetOwnPosition()
        {
            position = GetANPosition(this.WsnId);
        }

        //TEST
        //public void NewAnchor(string AnchorWsnId, double RSS, double posx, double posy, int van)
        //{
        //    if(van == 1)
        //    anchorList.Add(new AnchorNode(AnchorWsnId, posx, posy, RSS));
        //    else
        //        virtualAnchorList.Add(new AnchorNode(AnchorWsnId, posx, posy, RSS));
        //}

        private void UpdateAnchorPositions()
        {
            foreach (AnchorNode AN in this.Anchors)
            {
                Point newPosition = GetANPosition(AN.nodeid);

                AN.posx = newPosition.x;
                AN.posy = newPosition.y;
            }
        }

        private void RemoveOutdatedAnchors()
        {
            foreach (AnchorNode AN in this.Anchors)
            {
                if (AN.lastUpdate < DateTime.Now.Subtract(new TimeSpan(0, 2, 0)))
                    this.Anchors.Remove(AN);
            }
        }

        /// <summary>
        /// Retrieves the position of the specified node from the DB  
        /// </summary>
        /// <param name="AN">The WSNid of the Anchor Node</param>
        /// <returns>The position of the Anchor Node</returns>
        private Point GetANPosition(string AnchorWsnId)
        {
            DataSet returnSet;
            string cmd = "call getPositionWsnId('" + AnchorWsnId + "');";
            Point pos = new Point();

            try
            {
                returnSet = MyDb.Query(cmd);

                foreach (DataRow row in returnSet.Tables[0].Rows)
                {
                    pos.x = Convert.ToDouble(row["X"]);
                    pos.y = Convert.ToDouble(row["Y"]);
                }
            }
            catch (Exception e_mysql)
            {
                Console.WriteLine(e_mysql.Message);
                Console.WriteLine(e_mysql.StackTrace);
            }

            return pos;
        }

        private void NewAnchor(string AnchorWsnId, double RSS, int van, DateTime now)
        {
            Point ANpos = GetANPosition(AnchorWsnId);

            if (van == 1)
                anchorList.Add(new AnchorNode(AnchorWsnId, ANpos.x, ANpos.y, RSS, now));
            else
                virtualAnchorList.Add(new AnchorNode(AnchorWsnId, ANpos.x, ANpos.y, RSS, now));
        }

        #endregion Methods
    }
}