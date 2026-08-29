export default class LinqSettings {
    /**
     * Supported formats:
     *   "yyyy-MM-ddThh:mm:ss.sssZ"
     *   "yyyy-MM-ddThh:mm:ss+hh:mm"
     * Examples:
     *   "2019-09-25T16:00:20.817Z"
     *   "2019-09-25T16:00:20.817"
     *   "2019-09-25"
     *   "2019-09-24T00:00:00"
     *   "2019-09-24T00:00:00Z"
     *   "2019-10-14T21:00:00.000Z"
     *   "2019-10-16T00:00:00+03:00"
     */
    stringToDateCastRegex: RegExp;
    stringToDateCastResolver: ((date: any) => boolean);
    stringToDateCastEnabled: boolean;
}
